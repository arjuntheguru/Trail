import React from 'react';
import { Modal } from 'react-bootstrap';
import CompanyForm from './CompanyForm';

const EditCompany = ({ show, onClose, data }) => {

    return (
        <Modal show={show} onHide={onClose} centered size="lg">           
            <Modal.Body className="bgGradient">{<CompanyForm isEdit={true} data={data} onClose={onClose}/>}</Modal.Body>
        </Modal>
    )

}

export default EditCompany;