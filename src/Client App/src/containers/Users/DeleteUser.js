import React from 'react';
import { deleteUser } from '../../store/user-actions';

import { useDispatch } from "react-redux";
import DeleteModal from '../../components/DeleteModal/DeleteModal';

const DeleteUser = ({ show, onClose, data }) => {

    const dispatch = useDispatch();

    const deleteUserHandler = () => {        
        dispatch(deleteUser(data, false));
        onClose();
    }

    return (
        <DeleteModal show={show} onClose={onClose} data={`${data.firstName} ${data.lastName}`} handler={deleteUserHandler}/>
    )

}

export default DeleteUser;